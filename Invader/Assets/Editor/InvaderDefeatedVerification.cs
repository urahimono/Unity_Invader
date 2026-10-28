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
                .Select(inName => (EnemyShot)Read(Read(formation, "_enemyShots"), inName)).ToArray();
            var ufo = (Ufo)Read(formation, "_ufo");
            inDriver.EditorSetState(GameState.Playing);
            VerifyUfoControl(inDriver, formation, player, shot, ufo);
            VerifyAttackClock(formation, ufo);
            VerifyScoreReload(inDriver, formation, enemies);
            VerifyEnemyShotController(inDriver, formation, invaders, enemies);
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
            VerifyInvasion(inDriver, formation, invaders, enemies, ufo);
            UnityEngine.Object.DestroyImmediate(inDriver.GetComponent<DebugStatusOverlay>());
            UnityEngine.Object.DestroyImmediate(inDriver);
            Require(Read(formation, "Invaded") == null, "Driver destruction removes its invasion subscription.");
            Call(formation, "CheckInvasion", Vector2.down * 2f);
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

        private static void VerifyEnemyShotController(GameplayDriver inDriver, InvaderFormation inFormation,
            Invader[] inInvaders, EnemyShot[] inEnemies)
        {
            var controller = Read(inFormation, "_enemyShots");
            Call(inFormation, "BeginWave");
            for (int phase = 0; phase < 4; phase++)
            {
                Set(Read(inFormation, "_enemyShots"), "_shotTickElapsed", 0d);
                if (phase >= 2)
                {
                    foreach (var shot in inEnemies.Where(inShot => inShot.gameObject.activeSelf))
                    {
                        Set(shot, "_stepCount", 49);
                    }
                }
                Call(inFormation, "UpdateAttacks");
                Require((int)Read(Read(inFormation, "_enemyShots"), "_shotPhase") == new[] { 2, 1, 0, 2 }[phase], "Shared phase order 0/2/1/0.");
                Require(inEnemies.Count(inShot => inShot.gameObject.activeSelf) == Math.Min(phase, 3), "Rolling skips first attempt; each later phase launches its type.");
            }
            Call(controller, "SetFast", true);
            var counts    = inEnemies.Select(inShot => (int)Read(inShot, "_stepCount")).ToArray();
            var positions = inEnemies.Select(inShot => inShot.GetComponent<Rigidbody2D>().position).ToArray();
            inDriver.EditorSetState(GameState.Attract);
            Require(inEnemies.All(inShot => inShot.gameObject.activeSelf && (bool)Read(inShot, "_fast") &&
                inShot.GetComponent<Rigidbody2D>().simulated == false), "Bulk pause preserves all flights and speed.");
            inDriver.EditorSetState(GameState.Playing);
            Require(inEnemies.Select(inShot => (int)Read(inShot, "_stepCount")).SequenceEqual(counts) &&
                inEnemies.Select(inShot => inShot.GetComponent<Rigidbody2D>().position).SequenceEqual(positions), "Bulk resume preserves positions/counts.");
            var mode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                foreach (var shot in inEnemies)
                {
                    shot.GetComponent<Rigidbody2D>().linearVelocity = Vector2.down;
                }
                Call(controller, "PreparePhysicsStep");
                Physics2D.Simulate(Time.fixedDeltaTime);
                Require(inEnemies.Select(inShot => inShot.GetComponent<Rigidbody2D>().position).SequenceEqual(positions), "Bulk preparation cancels residual movement for all three shots.");
            }
            finally
            {
                Physics2D.simulationMode = mode;
            }
            int[] plunger  = { 1, 7, 1, 1, 1, 4, 11, 1, 6, 3, 1, 1, 11, 9, 2, 8 };
            int[] squiggly = { 11, 1, 6, 3, 1, 1, 11, 9, 2, 8, 2, 11, 4, 7, 10 };
            Call(inFormation, "BeginWave");
            for (int type = 1; type <= 2; type++)
            {
                var table = type == 1 ? plunger : squiggly;
                for (int index = 0; index <= table.Length; index++)
                {
                    if (type == 1)
                    {
                        Call(controller, "ProcessPlunger", 55);
                    }
                    else
                    {
                        Call(controller, "ProcessSquiggly");
                    }
                    var expected = inInvaders[44 + table[index % table.Length] - 1].transform.position - Vector3.up * 0.08f;
                    Require(inEnemies[type].gameObject.activeSelf && Vector2.Distance(inEnemies[type].transform.position, expected) < 1e-6f, "Column table cycles through lowest living muzzle.");
                    Call(inEnemies[type], "ResetFlight");
                }
            }
            Call(inFormation, "PreparePlayerRespawn");
            Require((int)Read(controller, "_plungerColumnIndex") == 1 && (int)Read(controller, "_squigglyColumnIndex") == 1 &&
                inEnemies.All(inShot => (int)Read(inShot, "_stepCount") == 0), "Respawn resets all flights while preserving table pointers.");
            Call(inFormation, "BeginWave");
            Require((int)Read(controller, "_plungerColumnIndex") == 0 && (int)Read(controller, "_squigglyColumnIndex") == 0 &&
                inEnemies.All(inShot => (bool)Read(inShot, "_fast") == false), "Wave resets table pointers and all speeds.");
            foreach (var invader in inInvaders.Where((inInvader, inIndex) => inIndex % 11 == 0))
            {
                invader.gameObject.SetActive(false);
            }
            Call(controller, "ProcessPlunger", 55);
            Require(inEnemies[1].gameObject.activeSelf == false && (int)Read(controller, "_plungerColumnIndex") == 1, "Empty column consumes table entry without fallback.");
            inInvaders[50].gameObject.SetActive(false);
            Call(controller, "ProcessPlunger", 55);
            Require(Vector2.Distance(inEnemies[1].transform.position, inInvaders[39].transform.position - Vector3.up * 0.08f) < 1e-6f, "Next table entry selects next living row.");
            Call(controller, "ResetFlights");
            Call(controller, "ProcessPlunger", 1);
            Require(inEnemies[1].gameObject.activeSelf == false, "Last living Invader prevents new Plunger launch.");
            Require((bool)typeof(EnemyShot).GetMethod("TryLaunch", PrivateInstance).Invoke(inEnemies[1], new object[] { Vector2.zero }), "Launch existing Plunger flight.");
            Call(controller, "ProcessPlunger", 1);
            Require((int)Read(inEnemies[1], "_stepCount") == 2, "An existing Plunger flight continues with one living Invader.");
            Call(controller, "ResetFlights");
            var origin = inFormation.GetComponent<Rigidbody2D>().position;
            Call(controller, "ProcessRolling", origin - Vector2.right, origin);
            Call(controller, "ProcessRolling", origin - Vector2.right, origin);
            Require(inEnemies[0].gameObject.activeSelf == false, "Rolling out of range is not clamped.");
            Call(controller, "ProcessRolling", origin, origin);
            Require(inEnemies[0].gameObject.activeSelf == false, "Rolling does not substitute for an empty column.");
            Call(inFormation, "BeginWave");
            Debug.Log("ENEMY_SHOT_CONTROLLER_PASS: phase order, bulk operations/physics preparation, tables/empty columns, Rolling limits and reset contracts.");
        }

        private static void VerifyScoreReload(GameplayDriver inDriver, InvaderFormation inFormation, EnemyShot[] inEnemies)
        {
            var controller = Read(inFormation, "_enemyShots");
            Require(Read(controller, "_getScore") is Func<int> &&
                typeof(InvaderFormation).GetFields(PrivateInstance).All(inField => inField.FieldType != typeof(GameplayDriver)),
                "Formation has a score reader and no Driver field.");
            try
            {
                typeof(InvaderFormation).GetMethod("Initialize", PrivateInstance).Invoke(inFormation, new object[] { null, null, null });
                throw new InvalidOperationException("Null score reader must be rejected at initialization.");
            }
            catch (TargetInvocationException exception)
            {
                Require(exception.InnerException is ArgumentNullException argument && argument.ParamName == "inGetScore",
                    "Null score reader validation precedes initialization.");
            }
            int initialScore = inDriver.Score;
            int[] scores     = { 0, 299, 300, 1099, 1100, 2099, 2100, 3099, 3100, 9999, 10000 };
            int[] rates      = { 48, 48, 16, 16, 11, 11, 8, 8, 7, 7, 7 };
            for (int index = 0; index < scores.Length; index++)
            {
                Set(inDriver, "_score", scores[index]);
                Call(inFormation, "PreparePlayerRespawn");
                Set(inEnemies[0], "_stepCount", rates[index]);
                Call(controller, "ProcessPlunger", Alive(inFormation));
                Require(inEnemies[1].gameObject.activeSelf == false, "Reload requires strictly more than the boundary StepCount.");
                Set(inEnemies[0], "_stepCount", rates[index] + 1);
                Call(controller, "ProcessPlunger", Alive(inFormation));
                Require(inEnemies[1].gameObject.activeSelf, "Actual firing uses current score's Reload rate and permits zero in the other slot.");
            }
            Set(inDriver, "_score", 299);
            Call(inFormation, "PreparePlayerRespawn");
            Set(inEnemies[0], "_stepCount", 17);
            Call(controller, "ProcessPlunger", Alive(inFormation));
            Require(inEnemies[1].gameObject.activeSelf == false, "Score 299 blocks StepCount 17.");
            Call(inDriver, "NotifyShotResolved", HitTargetType.InvaderBottom);
            Call(controller, "ProcessPlunger", Alive(inFormation));
            Require(inDriver.Score == 309 && inEnemies[1].gameObject.activeSelf,
                "Common scoring immediately changes the next firing decision through the original callback.");
            Set(inDriver, "_score", initialScore);
            Call(inFormation, "PreparePlayerRespawn");
            Debug.Log("SCORE_READER_PASS: null validation, live callback, Reload boundaries, strict StepCount and score-change firing.");
        }

        private static void VerifyInvasion(GameplayDriver inDriver, InvaderFormation inFormation, Invader[] inInvaders,
            EnemyShot[] inEnemies, Ufo inUfo)
        {
            Require(((Delegate)Read(inFormation, "Invaded")).GetInvocationList().Length == 1,
                "One Driver subscription after two Waves, pause and respawn.");
            Call(inFormation, "BeginWave");
            inDriver.EditorSetState(GameState.Playing);
            int notifications = 0;
            Action observe    = () => notifications++;
            inFormation.Invaded += observe;
            float invasionY = (40f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;
            var boundary    = new Vector2(0f, invasionY - inInvaders.Min(inInvader => inInvader.transform.localPosition.y));
            Call(inFormation, "CheckInvasion", boundary);
            Require(notifications == 0 && inDriver.CurrentState == GameState.Playing, "Exactly y=40 does not invade.");
            foreach (var invader in inInvaders.Skip(44))
            {
                invader.gameObject.SetActive(false);
            }
            Call(inFormation, "CheckInvasion", boundary - Vector2.up * ArcadeViewport.UnitsPerPixel);
            Require(notifications == 0, "Defeated lowest row below the threshold is ignored.");
            Call(inFormation, "BeginWave");
            int lives = inDriver.Lives;
            foreach (var state in new[] { GameState.Attract, GameState.PlayerDead, GameState.WaveClear, GameState.GameOver })
            {
                inDriver.EditorSetState(state);
                Call(inFormation, "CheckInvasion", boundary - Vector2.up * ArcadeViewport.UnitsPerPixel);
                Require(inDriver.CurrentState == state && inDriver.Lives == lives, "Non-Playing Driver ignores invasion.");
            }
            inDriver.EditorSetState(GameState.Playing);
            inDriver.enabled = false;
            Call(inFormation, "CheckInvasion", boundary - Vector2.up * ArcadeViewport.UnitsPerPixel);
            Require(inDriver.Lives == lives && inDriver.CurrentState == GameState.Playing, "Disabled Driver ignores invasion.");
            inDriver.enabled = true;
            notifications = 0;
            // Approach an edge from four pixels above the line; the existing eight-pixel descent crosses it.
            float maximumX = (ArcadeViewport.ScreenWidth / 2f - 8f - 10 * InvaderFormation.Spacing) * ArcadeViewport.UnitsPerPixel;
            var body       = inFormation.GetComponent<Rigidbody2D>();
            body.position = new Vector2(maximumX - ArcadeViewport.UnitsPerPixel, boundary.y + 4f * ArcadeViewport.UnitsPerPixel);
            Require((bool)typeof(Ufo).GetMethod("TryLaunch", PrivateInstance).Invoke(inUfo, new object[] { true }), "Launch UFO before invasion.");
            foreach (var enemy in inEnemies.Take(2))
            {
                Require((bool)typeof(EnemyShot).GetMethod("TryLaunch", PrivateInstance).Invoke(enemy, new object[] { Vector2.zero }), "Launch enemy shot before invasion.");
            }
            var steps       = inEnemies.Select(inEnemy => (int)Read(inEnemy, "_stepCount")).ToArray();
            var ufoPosition = inUfo.GetComponent<Rigidbody2D>().position;
            int ticks       = (int)Read(Read(inFormation, "_ufo"), "_ticksRemaining");
            int score       = inDriver.Score;
            int wave        = inDriver.WaveNumber;
            Call(inFormation, "SetDebugFastMovement", true);
            Set(inFormation, "_elapsed", 2f * 20f / 60f);
            Call(inFormation, "FixedUpdate");
            var stoppedPosition = body.position;
            Require(notifications == 1 && inDriver.Lives == 0 && inDriver.CurrentState == GameState.GameOver &&
                (bool)Read(inFormation, "_playing") == false && body.simulated == false &&
                Mathf.Abs(stoppedPosition.y - (boundary.y - 4f * ArcadeViewport.UnitsPerPixel)) < 1e-6f,
                "First fast movement step synchronously invades and stops remaining steps.");
            Call(inFormation, "FixedUpdate");
            Require(notifications == 1 && body.position == stoppedPosition && inDriver.Score == score && inDriver.WaveNumber == wave &&
                inEnemies.Select(inEnemy => (int)Read(inEnemy, "_stepCount")).SequenceEqual(steps) &&
                inEnemies.All(inEnemy => inEnemy.GetComponent<Rigidbody2D>().simulated == false) &&
                inUfo.GetComponent<Rigidbody2D>().position == ufoPosition && inUfo.GetComponent<Rigidbody2D>().simulated == false &&
                (int)Read(Read(inFormation, "_ufo"), "_ticksRemaining") == ticks, "Stopped formation cannot notify again or progress enemies/UFO.");
            inFormation.Invaded -= observe;
            Debug.Log("INVASION_EVENT_PASS: threshold/dead guard, Driver state guards, synchronous fast-step GameOver and single subscription.");
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
            int ticks    = (int)Read(Read(inFormation, "_ufo"), "_ticksRemaining");
            inDriver.EditorSetState(GameState.Attract);
            Call(inFormation, "UpdateAttacks");
            Require(inUfo.gameObject.activeSelf && inUfo.GetComponent<Rigidbody2D>().simulated == false &&
                inUfo.GetComponent<Rigidbody2D>().position == position &&
                (int)Read(Read(inFormation, "_ufo"), "_ticksRemaining") == ticks && (int)Read(Read(inFormation, "_enemyShots"), "_shotPhase") == 0 &&
                (double)Read(Read(inFormation, "_enemyShots"), "_shotTickElapsed") == 0d,
                "Pause preserves UFO occupancy, position and countdown while resetting the shared clock.");
            inDriver.EditorSetState(GameState.Playing);
            Require(inUfo.GetComponent<Rigidbody2D>().simulated && (int)Read(inUfo, "_direction") == 1 &&
                inPlayer.NormalShotEndCount == 3, "Resume preserves flight direction and history.");

            Call(inFormation, "PreparePlayerRespawn");
            var squiggly = (EnemyShot)Read(Read(inFormation, "_enemyShots"), "_squigglyShot");
            Require((bool)typeof(EnemyShot).GetMethod("TryLaunch", PrivateInstance).Invoke(squiggly, new object[] { Vector2.zero }),
                "Launch Squiggly before pending UFO.");
            Set(Read(inFormation, "_ufo"), "_pending", true);
            Set(Read(inFormation, "_enemyShots"), "_shotPhase", 2);
            Set(Read(inFormation, "_enemyShots"), "_shotTickElapsed", 0d);
            Call(inFormation, "UpdateAttacks");
            Require(inUfo.gameObject.activeSelf == false && (bool)Read(Read(inFormation, "_ufo"), "_pending") &&
                (int)Read(squiggly, "_stepCount") == 2, "Shared phase advances occupied Squiggly and leaves UFO pending.");
            Call(squiggly, "ResetFlight");
            VerifyPendingUfoLaunch(inFormation, inUfo, 3);
            Set(Read(inFormation, "_enemyShots"), "_shotPhase", 2);
            Set(Read(inFormation, "_enemyShots"), "_shotTickElapsed", 0d);
            Call(inFormation, "UpdateAttacks");
            Require(squiggly.gameObject.activeSelf == false, "UFO occupies the shared phase without launching Squiggly.");

            int lives = inDriver.Lives;
            Call(inDriver, "NotifyPlayerHit");
            Set(inDriver, "_playerDeadElapsed", 0.5f);
            Call(inDriver, "UpdatePlayerDead");
            Require(inDriver.CurrentState == GameState.Playing && inPlayer.NormalShotEndCount == 3 &&
                inUfo.gameObject.activeSelf == false && (bool)Read(Read(inFormation, "_ufo"), "_pending") == false &&
                (int)Read(Read(inFormation, "_ufo"), "_ticksRemaining") == 0x0600, "Death/respawn resets UFO but retains Player history.");
            VerifyPendingUfoLaunch(inFormation, inUfo, 3);
            // Restore the fixture for the existing two-wave scoring checks below.
            Set(inDriver, "_lives", lives);
            Call(inPlayer, "ResetShotHistory");
            Call(inFormation, "BeginWave");
            Debug.Log("UFO_CONTROL_PASS: Player-bound live history 0..3, Formation launches, shared phase, pause/resume and death/respawn.");
        }

        private static void VerifyAttackClock(InvaderFormation inFormation, Ufo inUfo)
        {
            var controller = Read(inFormation, "_enemyShots");
            var processUfo = (Func<bool, bool>)Read(controller, "_tryProcessUfoPhase");
            var countdown  = (Action)Read(controller, "_tickUfoCountdown");
            var order      = new System.Collections.Generic.List<string>();
            Call(inFormation, "BeginWave");
            Call(inFormation, "UpdateAttacks");
            Require(inUfo.gameObject.activeSelf == false && (int)Read(inUfo, "_ticksRemaining") == 0x0600 &&
                (bool)Read(inUfo, "_pending") == false, "Inactive UFO countdown waits for formation descent.");
            Call(inFormation, "BeginWave");
            inFormation.GetComponent<Rigidbody2D>().position -= Vector2.up;
            Set(Read(inFormation, "_ufo"), "_ticksRemaining", 0);
            Set(controller, "_tryProcessUfoPhase", (Func<bool, bool>)((squigglyIdle) =>
            {
                order.Add("ufo");
                return processUfo(squigglyIdle);
            }));
            Set(controller, "_tickUfoCountdown", (Action)(() =>
            {
                order.Add("tick" + Read(controller, "_shotPhase"));
                countdown();
            }));
            try
            {
                for (int step = 0; step < 5; step++)
                {
                    Call(inFormation, "UpdateAttacks");
                    if (step == 0)
                    {
                        Require(inUfo.gameObject.activeSelf == false && (bool)Read(inUfo, "_pending") &&
                            (int)Read(inUfo, "_ticksRemaining") == 0x0600 - 1,
                            "Inactive UFO expires after phase 0 and decrements on the same tick; launch waits for phase 2.");
                    }
                }
                Require(string.Join(",", order) == "tick0,ufo,tick2,tick1,tick0,ufo,tick2,tick1" &&
                    (int)Read(controller, "_shotPhase") == 0 &&
                    Math.Abs((double)Read(controller, "_shotTickElapsed")) < 1e-7d &&
                    (int)Read(Read(inFormation, "_ufo"), "_ticksRemaining") == 0x0600 - 6 && inUfo.gameObject.activeSelf,
                    "Five 0.02s updates execute six ordered 60Hz ticks, including two ticks in the last step.");
            }
            finally
            {
                Set(controller, "_tryProcessUfoPhase", processUfo);
                Set(controller, "_tickUfoCountdown", countdown);
            }
            var squiggly = (EnemyShot)Read(controller, "_squigglyShot");
            foreach (int alive in new[] { 7, 8 })
            {
                Call(inFormation, "BeginWave");
                Set(inFormation, "_aliveCount", alive);
                Set(Read(inFormation, "_ufo"), "_pending", true);
                Set(controller, "_shotPhase", 2);
                Call(inFormation, "UpdateAttacks");
                Require(inUfo.gameObject.activeSelf == (alive == 8) && squiggly.gameObject.activeSelf == (alive == 7),
                    "Shared phase preserves the eight-Invader UFO threshold.");
            }
            Call(inFormation, "PreparePlayerRespawn");
            Set(Read(inFormation, "_ufo"), "_pending", true);
            Call(inUfo, "SetPlaying", false);
            Set(controller, "_shotPhase", 2);
            Call(inFormation, "UpdateAttacks");
            Require(inUfo.gameObject.activeSelf == false && squiggly.gameObject.activeSelf == false &&
                (bool)Read(Read(inFormation, "_ufo"), "_pending") && (int)Read(controller, "_shotPhase") == 1,
                "Failed UFO launch consumes phase 2 without falling back to Squiggly.");
            Set(inUfo, "_ticksRemaining", 37);
            Call(inFormation, "SetPlaying", false);
            Call(inFormation, "UpdateAttacks");
            Call(inFormation, "SetPlaying", true);
            Require((bool)Read(inUfo, "_pending") && (int)Read(inUfo, "_ticksRemaining") == 37,
                "Pause/resume preserves inactive pending and countdown.");
            Require((bool)Call(inUfo, "ProcessPhase", true, true) && inUfo.gameObject.activeSelf &&
                (bool)Read(inUfo, "_pending") == false, "Successful phase launch alone consumes pending.");
            Set(inUfo, "_pending", true);
            Require(inUfo.TryReceiveHit(null) && (bool)Read(inUfo, "_pending") &&
                (int)Read(inUfo, "_ticksRemaining") == 37, "Defeat only resets flight, preserving the cycle.");
            Call(inUfo, "TryLaunch", true);
            inUfo.GetComponent<Rigidbody2D>().position = new Vector2(1000f, 0f);
            Require((bool)Call(inUfo, "ProcessPhase", true, false) && inUfo.gameObject.activeSelf == false &&
                (bool)Read(inUfo, "_pending") && (int)Read(inUfo, "_ticksRemaining") == 37,
                "Screen exit consumes the occupied phase and preserves pending/countdown.");
            Call(inFormation, "BeginWave");
            Require(inUfo.gameObject.activeSelf == false && (bool)Read(inUfo, "_pending") == false &&
                (int)Read(inUfo, "_ticksRemaining") == 0x0600, "Wave resets the complete UFO cycle.");
            Debug.Log("ATTACK_CLOCK_PASS: 60Hz residue/multiple ticks, countdown order, UFO threshold and failed launch.");
        }

        private static void VerifyPendingUfoLaunch(InvaderFormation inFormation, Ufo inUfo, long inCount)
        {
            Set(Read(inFormation, "_ufo"), "_pending", true);
            Set(Read(inFormation, "_enemyShots"), "_shotPhase", 0);
            Set(Read(inFormation, "_enemyShots"), "_shotTickElapsed", 0d);
            Call(inFormation, "UpdateAttacks");
            Require(inUfo.gameObject.activeSelf == false, "Phase 0 cannot launch UFO.");
            Call(inFormation, "UpdateAttacks");
            int direction = (inCount & 1L) != 0 ? 1 : -1;
            Require(inUfo.gameObject.activeSelf && (int)Read(inUfo, "_direction") == direction &&
                (bool)Read(Read(inFormation, "_ufo"), "_pending") == false && (int)Read(Read(inFormation, "_enemyShots"), "_shotPhase") == 1,
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

        private static object Call(object inTarget, string inName, params object[] inArguments)
        {
            return inTarget.GetType().GetMethod(inName, PrivateInstance).Invoke(inTarget, inArguments);
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
