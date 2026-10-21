using System;
using UnityEditor;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class PlayerMovementVerification
    {
        [MenuItem("Unity Invader/Verify Player Movement Logic")]
        public static void Verify()
        {
            foreach (int framesPerSecond in new[] { 30, 60, 120 })
            {
                var simulation = new PlayerSimulation();
                for (int frame = 0; frame < framesPerSecond; frame++)
                    simulation.Advance(1.0 / framesPerSecond, true, new PlayerInputState(false, true));
                Require(simulation.Player.X == 172, "Expected 60 logical ticks per second.");
            }

            var paused = new PlayerSimulation();
            paused.Advance(PlayerSimulation.TickDuration / 2, true, new PlayerInputState(true, false));
            Require(paused.Player.X == 112, "Sub-tick input must not move Player.");
            paused.Advance(10, false, new PlayerInputState(true, false));
            paused.Advance(PlayerSimulation.TickDuration / 2, true, new PlayerInputState(true, false));
            Require(paused.Player.X == 112, "Paused time and partial ticks must not carry over.");
            paused.Advance(PlayerSimulation.TickDuration / 2, true, new PlayerInputState(true, false));
            Require(paused.Player.X == 111, "Left input must move left.");
            paused.Advance(1, true, new PlayerInputState(true, true));
            Require(paused.Player.X == 111, "Both directions must cancel.");
            paused.Advance(10, true, new PlayerInputState(true, false));
            Require(paused.Player.X == PlayerModel.MinimumX, "Left bound failed.");
            paused.Advance(10, true, new PlayerInputState(false, true));
            Require(paused.Player.X == PlayerModel.MaximumX, "Right bound failed.");

            Debug.Log("PLAYER_MOVEMENT_LOGIC_PASS: 30/60/120fps; 60Hz; pause; both keys; bounds.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
