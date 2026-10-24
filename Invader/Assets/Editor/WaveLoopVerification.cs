using System;
using UnityEditor;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class WaveLoopVerification
    {
        [MenuItem("Unity Invader/Verify Wave Loop Logic")]
        public static void Verify()
        {
            InvaderHitVerification.Verify();
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var simulation = new GameplaySimulation();
                Require(simulation.WaveNumber == 1 && !simulation.WaveClearRequired, "Initial Wave must be 1.");
                simulation.StartNextWave();
                Require(simulation.WaveNumber == 1, "Cannot skip an uncleared wave.");
                for (int wave = 1; wave <= 2; wave++)
                {
                    Clear(simulation, 1.0 / fps);
                    var cleared = simulation.EditorCapture();
                    Require(cleared.WaveClearRequired && cleared.Formation.AliveCount == 0 &&
                        cleared.Score == wave * 990 && !cleared.Shot.Active && !cleared.Shot.FireReady,
                        "Final real hit must finish death, count, Shot and Score before clear.");
                    for (int frame = 0; frame < fps; frame++)
                    {
                        simulation.Advance(1.0 / fps, false, new PlayerInputState(true, true, true));
                    }
                    simulation.Advance(1, true, new PlayerInputState(false, true, true));
                    var frozen = simulation.EditorCapture();
                    Require(frozen.Player.Equals(cleared.Player) && frozen.Shot.Equals(cleared.Shot) &&
                        frozen.Formation.StepCount == cleared.Formation.StepCount && frozen.Score == cleared.Score,
                        "Clear must freeze all normal progress and input.");
                    simulation.StartNextWave();
                    Require(simulation.WaveNumber == wave + 1 && !simulation.WaveClearRequired,
                        "Next wave must start once.");
                    simulation.StartNextWave();
                    var next = simulation.EditorCapture();
                    Require(next.WaveNumber == wave + 1 && next.Player.Equals(cleared.Player) &&
                        next.Shot.Equals(cleared.Shot) && next.Score == wave * 990,
                        "Next wave must preserve Player/Shot/Score and release gate.");
                    var f = next.Formation;
                    Require(f.AliveCount == 55 && f.X == 32 && f.Y == 208 && f.Direction == 1 &&
                        f.StepCount == 0 && f.AnimationFrame == 0, "Formation must reset completely.");
                    foreach (var slot in f.Invaders)
                    {
                        Require(slot.Alive, "Every slot must be Alive.");
                    }
                    Require(cleared.Formation.AliveCount == 0 && cleared.Formation.Invaders[0].Alive == false,
                        "Retained snapshots must not change when the owned Formation resets.");
                    simulation.Advance(GameplaySimulation.TickDuration, true, new PlayerInputState(false, false, true));
                    var held = simulation.EditorCapture();
                    Require(!held.Shot.Active && !held.Shot.FireReady, "Held Space must not auto-fire in next wave.");
                    simulation.Advance(GameplaySimulation.TickDuration, true, default);
                    simulation.Advance(GameplaySimulation.TickDuration, true, new PlayerInputState(false, false, true));
                    Require(simulation.EditorCapture().Shot.Active, "Release then re-press must fire.");
                    simulation.EditorDeactivateShot();
                }
                Debug.Log($"WAVE_LOOP_LOGIC_PASS: fps={fps}; Wave={simulation.WaveNumber}; Score={simulation.Score}; Alive={simulation.EditorCapture().Formation.AliveCount}");
            }
        }

        internal static void Clear(GameplaySimulation inSimulation, double inElapsed)
        {
            Clear(inSimulation.EditorCapture, inSimulation.EditorPrepareShot, inSimulation.Advance, inElapsed);
        }

        internal static void Clear(GameplayDriver inDriver, double inElapsed)
        {
            Clear(() => inDriver.EditorCapture().Simulation, inDriver.EditorPrepareShot, inDriver.EditorAdvanceFixture, inElapsed);
        }

        // Fixtures arrange shots; the assertions still require actual tick collision and score rules.
        private static void Clear(Func<GameplayVerificationState> inCapture, Action<int, int> inPrepareShot,
            Action<double, bool, PlayerInputState> inAdvance, double inElapsed)
        {
            for (int row = 4; row >= 0; row--)
            {
                for (int column = 0; column < 11; column++)
                {
                    var before = inCapture();
                    inPrepareShot(before.Formation.X + column * 16, before.Formation.Y - row * 16 - 8);
                    inAdvance(inElapsed, true, new PlayerInputState(false, false, true));
                    if (inCapture().Shot.Active)
                    {
                        inAdvance(inElapsed, true, new PlayerInputState(false, false, true));
                    }
                    var after = inCapture();
                    Require(!after.Formation.Invaders[row * 11 + column].Alive && !after.Shot.Active,
                        "Each wave must be cleared with real shot collisions.");
                }
            }
        }

        internal static void Require(bool inCondition, string inMessage)
        {
            if (inCondition == false)
            {
                throw new InvalidOperationException(inMessage);
            }
        }
    }
}
