using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    [InitializeOnLoad]
    public static class ProjectileCancellationVerification
    {
        private static double _startAfter;

        private const string       PendingKey      = "UnityInvader.ProjectileCancellationVerification";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static ProjectileCancellationVerification()
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
                Debug.Log("PROJECTILE_CANCELLATION_PASS: three shot types, normal/B lifecycle, duplicate and pause guards, reuse.");
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
            var player     = UnityEngine.Object.FindAnyObjectByType<Player>();
            var formation  = UnityEngine.Object.FindAnyObjectByType<InvaderFormation>();
            var shot       = (PlayerShot)Read(player, "_shot");
            var controller = Read(formation, "_enemyShots");
            var enemies    = new[] { "_rollingShot", "_plungerShot", "_squigglyShot" }
                .Select(inName => (EnemyShot)Read(controller, inName)).ToArray();
            inDriver.EditorSetState(GameState.Playing);
            foreach (var enemy in enemies)
            {
                Call(enemy, "ResetFlight");
            }
            var shields = UnityEngine.Object.FindObjectsByType<Shield>(FindObjectsSortMode.None);
            var pixels  = shields.Select(inShield => ((bool[])Read(inShield, "_solid")).ToArray()).ToArray();
            int score   = inDriver.Score;
            int lives   = inDriver.Lives;
            int alive   = (int)Read(formation, "_aliveCount");
            var mode    = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                foreach (var enemy in enemies)
                {
                    long history = player.NormalShotEndCount;
                    Call(player, "SetInput", new PlayerInputState(false, false));
                    Call(player, "SetInput", new PlayerInputState(false, false, true));
                    Call(player, "UpdatePlayer");
                    Require(shot.gameObject.activeSelf && (bool)Read(player, "_shotOccupied"), "Normal shot occupies its slot after release/press.");
                    VerifyContact(shot, enemy);
                    Require(player.NormalShotEndCount == history + 1 && (bool)Read(player, "_shotOccupied") == false,
                        "Cancellation releases the slot and advances normal history exactly once.");
                }
                var bomb = inDriver.GetComponent<DebugCarpetBomb>();
                Require((bool)Call(bomb, "CreatePool"), "Existing B pool initializes.");
                var bombShot = ((PlayerShot[])Read(bomb, "_shots"))[0];
                long beforeB = player.NormalShotEndCount;
                Require(bombShot.TryLaunch(Vector2.zero), "B shot launches.");
                VerifyContact(bombShot, enemies[0]);
                Require(player.NormalShotEndCount == beforeB, "B cancellation does not advance normal history.");
                VerifyRejectedContacts(shot, enemies[0]);
                Require(inDriver.Score == score && inDriver.Lives == lives && inDriver.CurrentState == GameState.Playing &&
                    (int)Read(formation, "_aliveCount") == alive, "No scoring, life, state or Invader side effect.");
                Require(shields.Select((inShield, inIndex) => ((bool[])Read(inShield, "_solid")).SequenceEqual(pixels[inIndex])).All(inSame => inSame),
                    "Cancellation leaves all Shield pixels unchanged.");
            }
            finally
            {
                Call(shot, "SetPlaying", true);
                Call(shot, "ResetFlight");
                foreach (var enemy in enemies)
                {
                    Call(enemy, "SetPlaying", true);
                    Call(enemy, "ResetFlight");
                }
                Physics2D.simulationMode = mode;
            }
        }

        private static void VerifyContact(PlayerShot inShot, EnemyShot inEnemy)
        {
            int                   notifications = 0;
            Action<HitTargetType> onEnded       = inType =>
            {
                Require(inType == HitTargetType.None, "Cancellation reports None.");
                notifications++;
            };
            inShot.FlightEnded += onEnded;
            try
            {
                // Flush the inactive enemy's previous contact before reusing the same pair.
                Physics2D.Simulate(Time.fixedDeltaTime);
                Require((bool)Call(inEnemy, "TryLaunch", Vector2.zero), "Enemy slot can launch again.");
                inShot.transform.position                         = Vector2.zero;
                inShot.GetComponent<Rigidbody2D>().position       = Vector2.zero;
                inShot.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                Require(Physics2D.Simulate(Time.fixedDeltaTime), "Actual Physics2D step runs.");
                Require(inShot.gameObject.activeSelf == false && inEnemy.gameObject.activeSelf == false &&
                    (int)Read(inEnemy, "_stepCount") == 0 && notifications == 1, "Both flights end once, enemy count becomes zero.");
                Call(inShot, "OnTriggerEnter2D", inEnemy.GetComponent<Collider2D>());
                Call(inEnemy, "OnTriggerEnter2D", inShot.GetComponent<Collider2D>());
                Require(inEnemy.TryReceiveHit(inShot) == false && notifications == 1, "Residual callbacks cannot end either flight twice.");
            }
            finally
            {
                inShot.FlightEnded -= onEnded;
            }
        }

        private static void VerifyRejectedContacts(PlayerShot inShot, EnemyShot inEnemy)
        {
            Require((bool)Call(inEnemy, "TryLaunch", Vector2.zero), "Enemy relaunches for guards.");
            Require(inEnemy.TryReceiveHit(inShot) == false && inEnemy.TryReceiveHit(inEnemy) == false &&
                inEnemy.TryReceiveHit(null) == false, "Inactive player shot and wrong sources are rejected.");
            Require(inShot.TryLaunch(Vector2.zero), "Player shot relaunches for guards.");
            Call(inShot, "SetPlaying", false);
            Require(inEnemy.TryReceiveHit(inShot) == false, "Paused PlayerShot is rejected.");
            Call(inShot, "SetPlaying", true);
            Call(inEnemy, "SetPlaying", false);
            Require(inEnemy.TryReceiveHit(inShot) == false && inEnemy.gameObject.activeSelf &&
                inShot.gameObject.activeSelf && (int)Read(inEnemy, "_stepCount") == 1, "Paused EnemyShot preserves flights and count.");
            Call(inEnemy, "SetPlaying", true);
            Call(inEnemy, "ResetFlight");
            Require(inEnemy.TryReceiveHit(inShot) == false, "Reset enemy rejects a live PlayerShot.");
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
