using System;
using UnityEditor;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class PlayerShotVerification
    {
        [MenuItem("Unity Invader/Verify Player Shot Logic")]
        public static void Verify()
        {
            const double tick = PlayerSimulation.TickDuration;
            var fire          = new PlayerInputState(false, false, true);
            var release       = new PlayerInputState(false, false);
            var inactive      = new PlayerSimulation();
            inactive.Advance(10, false, fire);
            Require(!inactive.Shot.Active && inactive.Shot.FireReady, "Non-Playing input must not fire or change the gate.");
            var simulation = new PlayerSimulation();
            var shot       = simulation.Shot;
            simulation.Advance(tick, true, new PlayerInputState(false, true, true));
            Require(shot.Active && shot.X == 113 && shot.Y == 32, "Spawn must use current tick Player X and Y+8.");
            simulation.Advance(tick, true, new PlayerInputState(false, true, true));
            Require(shot.X == 113 && simulation.Player.X == 114 && shot.Y == 36, "Shot X must stay fixed; speed must be 4px/tick.");
            simulation.Advance(tick, true, release);
            simulation.Advance(tick, true, fire);
            Require(shot.Y == 44, "Press while active must not replace the slot.");
            simulation.Advance(51 * tick, true, fire);
            Require(shot.Active && shot.Y == 248, "Shot must remain active below upper edge.");
            simulation.Advance(tick, true, fire);
            Require(shot.Active && shot.Y == 252, "Shot must remain active at Y=252.");
            simulation.Advance(tick, true, fire);
            Require(!shot.Active && shot.Y == 256, "Shot must disappear at the upper edge.");
            simulation.Advance(2, true, fire);
            Require(!shot.Active && !shot.FireReady, "Held fire must not respawn after disappearance.");
            simulation.Advance(tick / 2, true, release);
            simulation.Advance(tick / 2, true, fire);
            Require(shot.Active && shot.X == 114 && shot.Y == 32, "Release between ticks must allow a fresh shot.");
            foreach (GameState state in Enum.GetValues(typeof(GameState)))
            {
                if (state == GameState.Playing)
                {
                    continue;
                }
                simulation.Advance(tick / 2, true, fire);
                int y = shot.Y;
                simulation.Advance(10, state == GameState.Playing, release);
                Require(shot.Active && shot.Y == y && !shot.FireReady, "Non-Playing must preserve and stop the slot/input state.");
                simulation.Advance(tick / 2, true, fire);
                Require(shot.Y == y, "Paused partial time must be discarded.");
                simulation.Advance(tick / 2, true, fire);
                Require(shot.Y == y + 4, "Resume must advance one tick.");
            }
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var timed = new PlayerSimulation();
                for (int frame = 0; frame < fps / 2; frame++)
                {
                    timed.Advance(1.0 / fps, true, new PlayerInputState(false, true, true));
                }
                Require(timed.Shot.Active && timed.Shot.X == 113 && timed.Shot.Y == 148 && timed.Player.X == 142,
                    "30/60/120fps must produce the same 30-tick result.");
                for (int frame = 0; frame < fps * 2; frame++)
                {
                    timed.Advance(1.0 / fps, true, fire);
                }
                Require(!timed.Shot.Active, "All FPS cases must disappear without held-fire respawn.");
            }
            InvaderFormationVerification.Verify();
            Debug.Log("PLAYER_SHOT_LOGIC_PASS: single slot; release gate; fixed X; spawn; 4px/tick; upper edge; all non-Playing states; 30/60/120fps; Player/Formation regression.");
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
