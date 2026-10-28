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
        private const string       PendingKey      = "UnityInvader.InvaderDefeatedVerification";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static InvaderDefeatedVerification()
        {
            EditorApplication.update += VerifyWhenPlaying;
        }

        // Batch entry: omit -quit so the domain reload and Play Mode can complete.
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void VerifyWhenPlaying()
        {
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
            var formation = UnityEngine.Object.FindAnyObjectByType<InvaderFormation>();
            var player    = UnityEngine.Object.FindAnyObjectByType<Player>();
            var shot      = (PlayerShot)Read(player, "_shot");
            var invaders  = formation.GetComponentsInChildren<Invader>(true);
            var enemies   = new[] { "_rollingShot", "_plungerShot", "_squigglyShot" }
                .Select(inName => (EnemyShot)Read(formation, inName)).ToArray();
            var ufo         = (Ufo)Read(formation, "_ufo");
            var ufoHandlers = Read(ufo, "Defeated");
            inDriver.EditorSetState(GameState.Playing);
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
